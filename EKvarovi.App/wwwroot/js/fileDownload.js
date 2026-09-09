// Blazor Server HttpClient poziva idu SA SERVERA, ne iz preglednika - obican
// C# fetch ne moze sam pokrenuti browser download. Zato server dohvati bajtove
// (vec kroz auth-ozicani HttpClient), posalje ih ovamo kao base64, a ovdje se
// tek grade Blob + privremeni <a download> koji preglednik stvarno pokrene.
window.downloadFileFromBytes = (fileName, contentType, base64Data) => {
    const byteCharacters = atob(base64Data);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: contentType });
    const url = URL.createObjectURL(blob);

    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
};
