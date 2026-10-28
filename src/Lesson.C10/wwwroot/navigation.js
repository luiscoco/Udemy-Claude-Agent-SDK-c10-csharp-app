(() => {
 const button=document.getElementById("student-theme-toggle");
 const key="c10-theme";
 let saved="light";try{saved=localStorage.getItem(key)||"light";}catch{}
 function apply(theme){document.documentElement.dataset.theme=theme;if(button){button.textContent=theme==="dark"?"Switch to light mode":"Switch to dark mode";button.setAttribute("aria-pressed",String(theme==="dark"));}}
 apply(saved==="dark"?"dark":"light");
 button?.addEventListener("click",()=>{const theme=document.documentElement.dataset.theme==="dark"?"light":"dark";apply(theme);try{localStorage.setItem(key,theme);}catch{}});
 function active(){const selected=location.pathname==="/guide.html"?"/guide.html"+(location.hash||"#learning"):location.pathname==="/architecture.html"?"/architecture.html":"/#application-lab";for(const a of document.querySelectorAll(".student-nav a")){if(a.getAttribute("href")===selected)a.setAttribute("aria-current","location");else a.removeAttribute("aria-current");}}
 active();window.addEventListener("hashchange",active);
})();
