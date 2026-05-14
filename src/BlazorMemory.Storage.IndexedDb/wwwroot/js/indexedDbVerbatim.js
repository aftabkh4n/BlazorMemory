const DB_NAME = "BlazorMemoryDB";
const STORE = "verbatim_memories";

function openDb() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, 1);

        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(STORE)) {
                const store = db.createObjectStore(STORE, { keyPath: "id" });
                store.createIndex("userId", "userId", { unique: false });
                store.createIndex("createdAt", "createdAt", { unique: false });
            }
        };

        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

export async function storeVerbatimMemory(memory) {
    const db = await openDb();
    const tx = db.transaction(STORE, "readwrite");
    tx.objectStore(STORE).put(memory);
    return tx.complete;
}

export async function searchVerbatimMemory(userId, query, limit) {
    const db = await openDb();
    const tx = db.transaction(STORE, "readonly");
    const store = tx.objectStore(STORE);

    const index = store.index("userId");
    const request = index.getAll(userId);

    return new Promise((resolve) => {
        request.onsuccess = () => {
            const results = request.result
                .filter(x =>
                    x.content.toLowerCase().includes(query.toLowerCase()))
                .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))
                .slice(0, limit);

            resolve(results);
        };
    });
}