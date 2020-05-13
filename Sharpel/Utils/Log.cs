using System;

public static class Log {
    // public static bool verbose;
    public static void Warning(string msg, params object[] args) {
        Stdout($"[Warning] {msg}", args);
    }
    public static void Stderr(string msg, params object[] args) {
        Console.Error.WriteLine(msg,args);
    }
    public static void Stdout(string msg, params object[] args) {
        Console.WriteLine(msg,args);
    }

}
