import { KeysDemo, type SceneProps } from './demos/KeysDemo';
import { MouseDemo } from './demos/MouseDemo';
import { FocusDemo } from './demos/FocusDemo';
import { DrawingDemo } from './demos/DrawingDemo';
import { PrivacyDemo } from './demos/PrivacyDemo';
import { CursorLensDemo } from './demos/CursorLensDemo';
const scenes = [KeysDemo, MouseDemo, FocusDemo, DrawingDemo, PrivacyDemo, CursorLensDemo];
export function FeatureDemo({index, ...props}: SceneProps & {index: number}) {
  const Scene = scenes[index];
  return <Scene {...props}/>;
}

