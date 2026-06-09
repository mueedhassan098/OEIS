// Saves a binary stream from Blazor as a file download (used for payslip PDFs).
window.oeisDownloadStream = async function (fileName, dotNetStreamRef) {
    const arrayBuffer = await dotNetStreamRef.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: 'application/pdf' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName || 'download.pdf';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
