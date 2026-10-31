import { createRoot, hydrateRoot } from 'react-dom/client';
import { App } from './App';
import './style.css';
const root=document.getElementById('root')!;
if(root.hasChildNodes()) hydrateRoot(root,<App initialPath={location.pathname}/>); else createRoot(root).render(<App initialPath={location.pathname}/>);

import './visuals.css';

if(import.meta.env.DEV && new URLSearchParams(location.search).has('inspect')) {
  import('./qa/inspect').then(module=>module.installInspector());
}

import './architecture.css';

import './program-window.css';

import './showcase.css';
import './v102.css';
import './integrations.css';
import './onboarding.css';
