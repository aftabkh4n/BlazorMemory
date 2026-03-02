namespace BlazorMemory.Core.Engine;

/// <summary>
/// Vector math utilities used by in-process storage adapters.
/// (Adapters backed by pgvector or a dedicated vector DB handle this server-side.)
/// </summary>
public static class VectorMath
{
    /// <summary>
    /// Calculates the cosine similarity between two float vectors.
    /// Returns a value between -1.0 and 1.0 (1.0 = identical direction).
    /// </summary>
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same dimensionality.");

        float dot = 0f, normA = 0f, normB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0f || normB == 0f) return 0f;

        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }
}
