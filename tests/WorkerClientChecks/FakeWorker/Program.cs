using System;
using System.IO;
using System.Text;
using System.Threading;
class FakeWorker
{
    static void Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);
        string line;
        while ((line = Console.ReadLine()) != null)
        {
            if (line == "QUIT") return;
            string[] fields = line.Split('|');
            string source = Encoding.UTF8.GetString(Convert.FromBase64String(fields[1]));
            if (source == "hang") { Thread.Sleep(Timeout.Infinite); return; }
            if (source == "error") { Console.WriteLine("ERR|" + Convert.ToBase64String(Encoding.UTF8.GetBytes("erro simulado"))); continue; }
            string path = Path.GetTempFileName();
            Console.WriteLine("OK|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(path)));
        }
    }
}
