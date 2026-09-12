(() => {
  const page = document.body.dataset.page || "";
  const pages = [
    ["index.html", "Download", "download"],
    ["help.html", "Quick help", "quick-help"],
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
  if (header) {
    header.innerHTML = `<div class="topbar"><div class="topbar-inner"><a class="brand" href="index.html"><img src="assets/recycle-mark.svg" alt="Sequencer Item Recovery mark"><span>Sequencer Item Recovery<small>For N.I.N.A.</small></span></a><button class="nav-toggle" type="button" aria-label="Toggle navigation" aria-expanded="true">Menu</button></div></div>`;
    const toggle = header.querySelector("button");
    toggle.addEventListener("click", () => {
      document.body.classList.toggle("sidebar-collapsed");
      toggle.setAttribute("aria-expanded", String(!document.body.classList.contains("sidebar-collapsed")));
    });
  }
  if (nav) {
    nav.innerHTML = pages.map(([href, label, id]) => `<a href="${href}" aria-label="${label}"${page === id ? ' aria-current="page"' : ""}><b class="nav-short" aria-hidden="true">${label.charAt(0)}</b><span>${label}</span></a>`).join("");
  }
  if (footer) {
    footer.innerHTML = `<div class="footer"><div class="footer-inner"><p>Sequencer Item Recovery is an independent plugin for N.I.N.A. · © 2026 Jost Jahn · <a href="mailto:AmrumSoftware@jostjahn.de">AmrumSoftware@jostjahn.de</a></p><p>Amrum Software is a project and collective name for the software offerings of Jost Jahn.</p></div></div>`;
  }
})();
