// BlazorMemory export helper
// Triggers a browser file download from a JSON string
window.BlazorMemoryExport = {
    download: function (fileName, content) {
        const blob = new Blob([content], { type: 'application/json' });
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href     = url;
        a.download = fileName;
        a.click();
        URL.revokeObjectURL(url);
    }
};