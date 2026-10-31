if (location.protocol === 'file:') {
  const language = navigator.language.slice(0, 2);
  const copy = {
    uk: ['Відкрийте Backpack через локальний сервер', 'У папці Website запустіть Start-Website.cmd подвійним клацанням. Сайт відкриється у браузері автоматично. Залиште вікно запуску відкритим.', 'Або виконайте в цій папці:'],
    en: ['Open Backpack through a local server', 'Double-click Start-Website.cmd in the Website folder. The website will open automatically. Keep the launcher window open.', 'Or run this command in that folder:'],
  };
  const text = copy[language] || copy.en;
  document.documentElement.lang = copy[language] ? language : 'en';
  document.body.style.cssText = 'margin:0;background:#080d0b;color:#f2f5f3;font:16px/1.7 system-ui,sans-serif;padding:clamp(24px,7vw,100px)';
  const help = document.createElement('section');
  help.style.cssText = 'max-width:720px;margin:5vh auto;padding:32px;border:1px solid #315b43;border-radius:18px;background:#111513';
  const brand = document.createElement('div');
  brand.textContent = 'Backpack';
  brand.style.cssText = 'color:#31c99a;font-weight:700;font-size:24px';
  const heading = document.createElement('h1');
  heading.textContent = text[0];
  heading.style.cssText = 'font-size:30px;line-height:1.25';
  const description = document.createElement('p');
  description.textContent = text[1];
  const alternative = document.createElement('p');
  alternative.textContent = text[2];
  const command = document.createElement('code');
  command.textContent = 'node node_modules/vite/bin/vite.js --host 127.0.0.1 --open';
  command.style.cssText = 'display:block;color:#31c99a;overflow-wrap:anywhere';
  help.append(brand, heading, description, alternative, command);
  document.body.prepend(help);
}
