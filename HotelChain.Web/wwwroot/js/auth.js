window.auth = {
    setToken: (t) => localStorage.setItem("jwt", t),
    getToken: () => localStorage.getItem("jwt"),
    clearToken: () => localStorage.removeItem("jwt")
};
window.openPdfBase64 = (b64) => {
  const url = "data:application/pdf;base64," + b64;
  window.open(url, "_blank");
};