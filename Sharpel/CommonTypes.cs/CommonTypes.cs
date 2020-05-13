using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using RoslynUtils;

public class CommonTypes {
    public readonly HashSet<INamedTypeSymbol> collectionTypes;

    public CommonTypes(Compilation compilation) {
        this.collectionTypes = new HashSet<INamedTypeSymbol>();
        collectCollectionType(compilation,collectionTypes,"FrozenArray`1");
        collectCollectionType(compilation,collectionTypes,"PatchableList`1");


        static void collectCollectionType(Compilation compilation, HashSet<INamedTypeSymbol> types, string name) {
            var _type = compilation.GetTypeByMetadataName(name);
            if (_type == null) {
                Log.Stderr($"unable to initialize collectionType lookup for {name}");
            }
            types.Add(_type);
        }

    }

    // todo int[] is not a named type, classify array types as collections?

    public bool IsCollectionType(ITypeSymbol other) {
        foreach (var _type in collectionTypes) {
            if (_type.EqualTo(other)) {
                return true;
            }
        }
        return false;
    }


}
