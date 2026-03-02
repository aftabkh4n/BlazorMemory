/**
 * blazorMemory.js
 * Browser-side IndexedDB operations for BlazorMemory.
 * Loaded as a JS module via IJSRuntime in Blazor WASM.
 *
 * Database schema:
 *   DB name  : BlazorMemoryDb
 *   Store    : memories
 *   KeyPath  : id
 *   Indexes  : userId (for filtering by user)
 */

const DB_NAME = 'BlazorMemoryDb';
const DB_VERSION = 1;
const STORE_NAME = 'memories';

// ─── Database Init ────────────────────────────────────────────────────────────

let _db = null;

function openDb() {
    if (_db) return Promise.resolve(_db);

    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);

        request.onupgradeneeded = (event) => {
            const db = event.target.result;

            if (!db.objectStoreNames.contains(STORE_NAME)) {
                const store = db.createObjectStore(STORE_NAME, { keyPath: 'id' });
                store.createIndex('userId', 'userId', { unique: false });
            }
        };

        request.onsuccess = (event) => {
            _db = event.target.result;
            resolve(_db);
        };

        request.onerror = (event) => {
            reject(new Error(`IndexedDB open failed: ${event.target.error}`));
        };
    });
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function txStore(db, mode) {
    const tx = db.transaction(STORE_NAME, mode);
    return tx.objectStore(STORE_NAME);
}

function promisify(request) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result);
        request.onerror  = () => reject(request.error);
    });
}

// ─── Cosine Similarity ───────────────────────────────────────────────────────

/**
 * Computes cosine similarity between two Float32Arrays.
 * Returns a value between -1 and 1. Returns 0 if either vector is zero.
 */
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

// ─── CRUD Operations ─────────────────────────────────────────────────────────

/**
 * Adds or replaces a memory entry.
 * entry.embedding should be a regular JS array of numbers (serialized from C# float[]).
 */
export async function addEntry(entry) {
    const db = await openDb();
    const store = txStore(db, 'readwrite');
    await promisify(store.put(entry));
    return entry.id;
}

/**
 * Updates an existing entry (same as add — IndexedDB uses put for upsert).
 */
export async function updateEntry(entry) {
    return addEntry(entry);
}

/**
 * Deletes a memory entry by id.
 */
export async function deleteEntry(id) {
    const db = await openDb();
    const store = txStore(db, 'readwrite');
    await promisify(store.delete(id));
}

/**
 * Gets a single memory entry by id. Returns null if not found.
 */
export async function getEntry(id) {
    const db = await openDb();
    const store = txStore(db, 'readonly');
    const result = await promisify(store.get(id));
    return result ?? null;
}

/**
 * Lists all memory entries for a given userId, sorted by most recent first.
 */
export async function listEntries(userId) {
    const db = await openDb();
    const store = txStore(db, 'readonly');
    const index = store.index('userId');
    const results = await promisify(index.getAll(userId));
    return results.sort((a, b) =>
        new Date(b.updatedAt ?? b.learnedAt) - new Date(a.updatedAt ?? a.learnedAt)
    );
}

/**
 * Performs a vector similarity search for a given userId.
 * Returns up to `limit` entries with similarity >= `threshold`, sorted desc.
 *
 * @param {number[]} queryEmbedding  - The query vector as a plain JS array
 * @param {string}   userId          - Filter to this user's memories only
 * @param {number}   limit           - Max results to return
 * @param {number}   threshold       - Minimum cosine similarity (0.0–1.0)
 */
export async function searchSimilar(queryEmbedding, userId, limit, threshold) {
    const db = await openDb();
    const store = txStore(db, 'readonly');
    const index = store.index('userId');
    const entries = await promisify(index.getAll(userId));

    const queryVec = new Float32Array(queryEmbedding);

    const scored = entries
        .map(entry => ({
            entry,
            score: cosineSimilarity(queryVec, new Float32Array(entry.embedding))
        }))
        .filter(x => x.score >= threshold)
        .sort((a, b) => b.score - a.score)
        .slice(0, limit);

    // Attach the relevance score onto each entry before returning
    return scored.map(x => ({ ...x.entry, relevanceScore: x.score }));
}

/**
 * Deletes all memory entries for a given userId.
 */
export async function clearEntries(userId) {
    const db = await openDb();
    const store = txStore(db, 'readwrite');
    const index = store.index('userId');
    const keys = await promisify(index.getAllKeys(userId));
    await Promise.all(keys.map(key => promisify(store.delete(key))));
}
