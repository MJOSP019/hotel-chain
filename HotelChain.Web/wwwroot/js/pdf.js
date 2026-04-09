window.openPdfBase64 = (base64) => {
    try {
        if (!base64) {
            console.error("Base64 vacío");
            return;
        }

        // si viene con data:application/pdf;base64
        const clean = base64.includes("base64,")
            ? base64.split("base64,")[1]
            : base64;

        // convertir base64 a bytes
        const binary = atob(clean);
        const bytes = new Uint8Array(binary.length);

        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }

        // crear blob PDF
        const blob = new Blob([bytes], { type: "application/pdf" });
        const url = URL.createObjectURL(blob);

        // abrir en nueva pestaña
        window.open(url, "_blank");
    } catch (err) {
        console.error("Error abriendo PDF:", err);
        alert("No se pudo abrir el PDF");
    }
};

window.downloadFileFromBase64 = (file) => {
    try {
        console.log("downloadFileFromBase64 recibido:", file);

        if (!file || !file.base64) {
            console.error("Archivo base64 vacío");
            alert("Archivo base64 vacío");
            return;
        }

        const clean = file.base64.includes("base64,")
            ? file.base64.split("base64,")[1]
            : file.base64;

        const binary = atob(clean);
        const bytes = new Uint8Array(binary.length);

        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }

        const blob = new Blob([bytes], {
            type: file.contentType || "application/octet-stream"
        });

        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");

        a.href = url;
        a.download = file.fileName || "archivo";
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);

        URL.revokeObjectURL(url);
    } catch (err) {
        console.error("Error descargando archivo:", err);
        alert("No se pudo descargar el archivo");
    }
};