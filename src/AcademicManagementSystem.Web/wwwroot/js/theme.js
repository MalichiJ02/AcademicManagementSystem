(() => {
    const root = document.documentElement;
    const preference = window.matchMedia("(prefers-color-scheme: dark)");
    const key = "ams-theme";
    let selected;
    try { selected = localStorage.getItem(key); } catch {}
    if (selected !== "dark" && selected !== "light") selected = null;

    const apply = () => {
        root.dataset.bsTheme = selected ?? (preference.matches ? "dark" : "light");
        const toggle = document.getElementById("themeToggle");
        if (toggle) toggle.setAttribute("aria-pressed", String(root.dataset.bsTheme === "dark"));
    };
    apply();
    preference.addEventListener("change", apply);
    document.addEventListener("DOMContentLoaded", () => {
        const toggle = document.getElementById("themeToggle");
        toggle.addEventListener("click", () => {
            selected = root.dataset.bsTheme === "dark" ? "light" : "dark";
            try { localStorage.setItem(key, selected); } catch {}
            apply();
        });
        apply();
    });
})();
