using System;

namespace Amity
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var window = new WindowBase();
            bool hasPainted = false;
            window.Paint += () =>
            {
                if (hasPainted) { return; }
                hasPainted = true;
                // TEMP
                for (int i = 0; i < window.Buffer.Length; i++)
                {
                    if ((i % 2) == 0) { continue; }
                    var t = (i / 400) % (256 * 3);
                    var r = t < 256 ? t : (t < 512 ? 512 - t : 0);
                    var g = t < 256 ? 0 : (t < 512 ? t - 256 : 768 - t);
                    var b = t < 256 ? 256 - t : (t < 512 ? 0 : t - 512);
                    window.Buffer[i] = new Color32
                    {
                        R = (byte)r,
                        G = (byte)g,
                        B = (byte)b,
                        A = 255,
                    };
                }
            };
            window.Show();
        }
    }
}
