// The wasm SDK includes this entry point in published output.
// GitHub Pages embeds the same runtime through docs/playground-gorm-runtime.js.
import { dotnet } from './_framework/dotnet.js';
await dotnet.run();
