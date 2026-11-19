namespace RevitFamilyImporter.LinkConflict.Services
{
    internal static class GeometryOutlinePolicy
    {
        // Shells com faces ou arestas devem continuar elegíveis para análise aproximada.
        internal static bool IsEmptySolid(int faceCount, int edgeCount) => faceCount == 0 && edgeCount == 0;
    }
}
