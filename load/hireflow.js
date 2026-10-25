// Нагрузка на путь бота без откликов: список открытых вакансий и подбор вакансий по резюме.
// Запуск — make load (docs/drills/README.md). Ничего не создаёт, кроме строк лимита загрузок в resume_uploads.
import http from 'k6/http';
import exec from 'k6/execution';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://web/api';
const API_KEY = __ENV.BotApi__ApiKey;
const RATE = parseInt(__ENV.RATE || '10', 10);
const DURATION = __ENV.DURATION || '4m';

// Тестовый диапазон Telegram ID: настоящих кандидатов с такими id нет, очистка — по telegram_id >= 990000000000.
const TELEGRAM_ID_BASE = 990000000000;

const resume = open('./resume.pdf', 'b');

export const options = {
  scenarios: {
    bot: {
      executor: 'ramping-arrival-rate',
      startRate: 0,
      timeUnit: '1s',
      preAllocatedVUs: 20,
      maxVUs: 200,
      stages: [
        { target: RATE, duration: '1m' },
        { target: RATE, duration: DURATION },
      ],
    },
  },
  // SLO из design.md §16: 99,5% ответов без ошибок, 95% быстрее 1 с.
  thresholds: {
    http_req_failed: ['rate<0.005'],
    http_req_duration: ['p(95)<1000'],
  },
};

// Заголовки бота: ключ только в заголовке, в вывод не попадает.
function headers() {
  return { 'X-Api-Key': API_KEY };
}

// Проверяет, что стенд отвечает и ключ подходит, до начала нагрузки.
export function setup() {
  if (!API_KEY) {
    exec.test.abort('Не задан BotApi__ApiKey: docker-compose.yml передаёт его в k6 из .env');
  }

  const res = http.get(`${BASE_URL}/vacancies?status=open`, { headers: headers(), tags: { name: 'vacancies' } });
  if (res.status !== 200) {
    exec.test.abort(`GET /vacancies вернул ${res.status}: стенд не поднят (make up) или ключ бота не подходит`);
  }

  if (res.json().length === 0) {
    console.warn('Открытых вакансий нет: подбору не с чем сравнивать, нагружается только извлечение текста');
  }
}

// Одна итерация: как кандидат в боте — открытые вакансии, затем подбор по резюме.
export default function () {
  const vacancies = http.get(`${BASE_URL}/vacancies?status=open`, {
    headers: headers(),
    tags: { name: 'vacancies' },
  });
  check(vacancies, { 'vacancies 200': (r) => r.status === 200 });

  const telegramId = TELEGRAM_ID_BASE + exec.vu.idInTest;
  const matches = http.post(
    `${BASE_URL}/candidates/${telegramId}/matches`,
    { file: http.file(resume, 'resume.pdf', 'application/pdf'), yearsOfExperience: '5' },
    { headers: headers(), tags: { name: 'matches' } },
  );
  check(matches, { 'matches 200': (r) => r.status === 200 });
}
