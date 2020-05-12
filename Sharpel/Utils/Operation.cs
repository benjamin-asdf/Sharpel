using System;
using System.Threading.Tasks;

namespace Sharpel {

    public static class Operation {

        public static void Retry(int maxTries, Action action) {
            for (var i = 0; i < maxTries; i++) {
                try {
                    action();
                    return;
                } catch (Exception ex) {
                    if (i >= maxTries) {
                        Console.Error.WriteLine($"Too many tries, exception follows");
                        Console.Error.Write(ex);
                    }
                    Task.Delay(200).RunSynchronously();
                }
            }
        }


    }

}
