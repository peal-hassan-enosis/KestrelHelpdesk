export function downloadPdf(fileName, base64Data) {
    if (!fileName || !base64Data) {
        throw new Error("A file name and PDF content are required.");
    }

    const binary = atob(base64Data);
    const bytes = new Uint8Array(binary.length);

    for (let index = 0; index < binary.length; index++) {
        bytes[index] = binary.charCodeAt(index);
    }

    const blob = new Blob([bytes], { type: "application/pdf" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");

    link.href = url;
    link.download = fileName;
    link.style.display = "none";
    document.body.appendChild(link);

    try {
        link.click();
    } finally {
        link.remove();

        // Give the browser time to begin the download before revoking the object URL.
        window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
}
