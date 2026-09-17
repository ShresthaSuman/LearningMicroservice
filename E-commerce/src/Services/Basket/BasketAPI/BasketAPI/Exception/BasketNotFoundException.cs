namespace BasketAPI.Exception;

public class BasketNotFoundException : System.Exception
{
    public  BasketNotFoundException(string message) : base(message)
    {
       
    }
}