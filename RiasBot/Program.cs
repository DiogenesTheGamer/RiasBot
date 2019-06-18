namespace RiasBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new RiasBot().StartAsync().GetAwaiter().GetResult();
        }
    }
}