(() => {
  const page = document.body.dataset.page || "";
  const pages = [
    ["index.html", "My plugins", "home"],
    ["sequencer-item-recovery.html", "Sequencer Item Recovery", "recovery"],
    ["install.html", "Install", "install"],
    ["help.html", "Quick help", "help"],
    ["detailed-guide.html", "Detailed guide", "detailed-guide"],
    ["faq.html", "FAQ", "faq"],
    ["versions.html", "Versions", "versions"],
    ["cli-parameters.html", "CLI parameters", "cli"],
    ["license.html", "License", "license"],
    ["legal.html", "Legal notice", "legal"],
    ["privacy.html", "Privacy", "privacy"]
  ];
  const nav = document.querySelector("#site-nav");
  const header = document.querySelector("#site-header");
  const footer = document.querySelector("#site-footer");
  const mobileNavigation = window.matchMedia("(max-width: 760px)");
  const setNavigationCollapsed = (collapsed) => {
    document.body.classList.toggle("sidebar-collapsed", collapsed);
    const toggle = header?.querySelector("button");
    if (toggle) toggle.setAttribute("aria-expanded", String(!collapsed));
  };
  if (header) {
    header.innerHTML = `<div class="topbar"><div class="topbar-inner"><a class="brand" href="index.html"><img src="assets/my-nina-plugins-mark.svg" alt="My N.I.N.A. Plugins mark"><span>My N.I.N.A. Plugins<small>Plugins by Jost Jahn</small></span></a><button class="nav-toggle" type="button" aria-label="Toggle navigation" aria-expanded="true">Menu</button></div></div>`;
    const toggle = header.querySelector("button");
    toggle.addEventListener("click", () => {
      setNavigationCollapsed(!document.body.classList.contains("sidebar-collapsed"));
    });
    const syncNavigationForViewport = () => setNavigationCollapsed(mobileNavigation.matches);
    mobileNavigation.addEventListener("change", syncNavigationForViewport);
    syncNavigationForViewport();
  }
  if (nav) {
    nav.innerHTML = pages.map(([href, label, id]) => `<a href="${href}" aria-label="${label}"${page === id ? ' aria-current="page"' : ""}><b class="nav-short" aria-hidden="true">${label.charAt(0)}</b><span>${label}</span></a>`).join("");
  }
  if (footer) {
    footer.innerHTML = `<div class="footer"><div class="footer-inner"><p>My N.I.N.A. Plugins · © 2026 Jost Jahn · <a href="mailto:AmrumSoftware@jostjahn.de">AmrumSoftware@jostjahn.de</a></p><p>Amrum Software is a project and collective name for the software offerings of Jost Jahn. N.I.N.A. is an independent third-party project.</p></div></div>`;
  }
  document.querySelectorAll("table[data-sortable]").forEach((table) => {
    const headers = [...table.querySelectorAll("thead th")];
    const body = table.tBodies[0];
    if (!body) return;
    headers.forEach((header, column) => {
      let ascending = true;
      header.tabIndex = 0;
      header.setAttribute("role", "button");
      header.setAttribute("aria-sort", "none");
      const sort = () => {
        const rows = [...body.rows];
        rows.sort((left, right) => left.cells[column].textContent.trim()
          .localeCompare(right.cells[column].textContent.trim(), undefined,
            { numeric: true, sensitivity: "base" }) * (ascending ? 1 : -1));
        rows.forEach((row) => body.append(row));
        headers.forEach((item) => item.setAttribute("aria-sort", "none"));
        header.setAttribute("aria-sort", ascending ? "ascending" : "descending");
        ascending = !ascending;
      };
      header.addEventListener("click", sort);
      header.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          sort();
        }
      });
    });
  });
})();
