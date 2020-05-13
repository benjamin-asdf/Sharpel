using System;

public static class Utils {

    public static string LowerFirstChar(string s) {
        return AdjustFirstChar(s,char.ToLower);
    }

    public static string UpperFistChar(string s) {
        return AdjustFirstChar(s,char.ToUpper);
    }

    static string AdjustFirstChar(string s, Func<char,char> op) {
        if (!String.IsNullOrWhiteSpace(s)) {
            var c = op(s[0]);
            s = s.Length > 1 ? $"{c}{s.Substring(1)}" : c.ToString();
        }
        return s;
    }
}
