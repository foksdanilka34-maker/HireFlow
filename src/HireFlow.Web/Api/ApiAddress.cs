namespace HireFlow.Web.Api;

public static class ApiAddress
{
    // Возвращает адрес API: относительный достраивается от адреса кабинета, в конце всегда «/».
    public static Uri Resolve(string hostBaseAddress, string apiBaseUrl)
    {
        var address = new Uri(new Uri(hostBaseAddress), apiBaseUrl).ToString();
        return new Uri(address.EndsWith('/') ? address : address + "/");
    }
}
