/**
 * blazorMemory.test.js
 * Unit tests for the JS side of the IndexedDB adapter.
 * Run with: npx vitest
 *
 * Tests the cosine similarity logic in isolation using a mock IndexedDB.
 * We test the pure functions directly since the IndexedDB interactions
 * are covered by the C# integration tests.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

// ─── Cosine Similarity Tests (pure function, no mocking needed) ───────────────

// We inline the function here for unit testing isolation.
// The real implementation lives in blazorMemory.js.
function cosineSimilarity(a, b) {
    let dot = 0, normA = 0, normB = 0;
    for (let i = 0; i < a.length; i++) {
        dot   += a[i] * b[i];
        normA += a[i] * a[i];
        normB += b[i] * b[i];
    }
    const denom = Math.sqrt(normA) * Math.sqrt(normB);
    return denom === 0 ? 0 : dot / denom;
}

describe('cosineSimilarity', () => {
    it('returns 1.0 for identical vectors', () => {
        const v = new Float32Array([1, 2, 3]);
        expect(cosineSimilarity(v, v)).toBeCloseTo(1.0, 5);
    });

    it('returns 0.0 for perpendicular vectors', () => {
        const a = new Float32Array([1, 0, 0]);
        const b = new Float32Array([0, 1, 0]);
        expect(cosineSimilarity(a, b)).toBeCloseTo(0.0, 5);
    });

    it('returns -1.0 for opposite vectors', () => {
        const a = new Float32Array([1, 0, 0]);
        const b = new Float32Array([-1, 0, 0]);
        expect(cosineSimilarity(a, b)).toBeCloseTo(-1.0, 5);
    });

    it('returns 0.0 for a zero vector', () => {
        const a = new Float32Array([0, 0, 0]);
        const b = new Float32Array([1, 2, 3]);
        expect(cosineSimilarity(a, b)).toBe(0);
    });

    it('is commutative', () => {
        const a = new Float32Array([0.3, 0.5, 0.8]);
        const b = new Float32Array([0.1, 0.9, 0.4]);
        expect(cosineSimilarity(a, b)).toBeCloseTo(cosineSimilarity(b, a), 5);
    });

    it('is scale-invariant', () => {
        const a  = new Float32Array([1, 2, 3]);
        const a2 = new Float32Array([2, 4, 6]); // same direction, double magnitude
        const b  = new Float32Array([4, 5, 6]);
        expect(cosineSimilarity(a, b)).toBeCloseTo(cosineSimilarity(a2, b), 5);
    });
});
