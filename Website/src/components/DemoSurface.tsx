import {Pencil,Languages,Scan,ZoomIn,Timer} from 'lucide-react';
import {DrawingDemo} from './demos/DrawingDemo';
import {FocusDemo} from './demos/FocusDemo';
import {CursorLensDemo} from './demos/CursorLensDemo';
import {TimerDemo,LayoutDemo} from './demos/PluginDemos';
import type {SceneProps} from './demos/KeysDemo';
import type {ModelCopy} from '../i18n';
export const pluginIcons=[Pencil,Languages,Scan,Scan,ZoomIn,Timer];
export function DemoSurface({index,a,...props}:SceneProps&{index:number;a:ModelCopy}){
 return index===0?<DrawingDemo {...props}/>:index===1?<LayoutDemo {...props} a={a}/>:index===2?<FocusDemo {...props} kind="lens"/>:index===3?<FocusDemo {...props} kind="focus"/>:index===4?<CursorLensDemo {...props}/>:<TimerDemo {...props} a={a}/>;
}
