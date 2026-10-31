import {useState} from 'react';
import {FocusDemo} from './demos/FocusDemo';
import {Switcher} from './Controls';
import type {SceneProps} from './demos/KeysDemo';
import type {ModelCopy} from '../i18n';
export function FocusCompare({a,...props}:SceneProps&{a:ModelCopy}){const [mode,setMode]=useState(0);return <div className="focus-compare"><div className="comparison-scene" key={mode}><FocusDemo {...props} kind={mode?'focus':'lens'}/></div><Switcher labels={[a.pluginNames[2],a.pluginNames[3]]} value={mode} onChange={setMode} label={a.pluginsTab}/></div>;}
