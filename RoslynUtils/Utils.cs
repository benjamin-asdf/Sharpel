using Microsoft.CodeAnalysis;

namespace RoslynUtils {
    public static class Utils {
        public static bool EqualToAny(this ITypeSymbol that, params ITypeSymbol[] others) {
            foreach (var typeSymbol in others) {
                if (that.EqualTo(typeSymbol)) return true;
            }

            return false;

        }
        public static bool EqualTo(this ITypeSymbol that, ITypeSymbol other) {
            if(ReferenceEquals(that,other)) return true;
            if(ReferenceEquals(that,null)) return false;
            if(ReferenceEquals(other,null)) return false;
            if(that.Equals(other)) return true;
            return that.MetadataName == other.MetadataName;
        }


        public static T NormWhiteSpaceLF<T>(this T node) where T : SyntaxNode {
            return node.NormalizeWhitespace(indentation: "    ", eol: "\n");
        }


    }

}
