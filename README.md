### SourceGenerator 
`./Editor/SourceGenerator` is a Class Library .NET project.
It's artifact is a `DLL` which goes to package's `./Editor/Plugins` folder to be shipped within UPM package.

### Usage (see [Unity documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/create-source-generator.html))

1. Set (via `./Packages/manifest.json`) the UPM package as the main project's dependency.
2. Optional, if there are explicit assembly definition files in the main project.
   Refer package's `IV.UnityBinder` assembly as a dependency to main project's assemblies that need to be processed (for which the source generation should happen).

### Compiling the source generator's DLL

- In VSCode there is a build task that will prepare a release build and copy it to an appropriate location. See `.vscode/tasks.json`.
- To manually build (requires .NET SDK) the DLL execute within SourceGenerator's root directory 
  > dotnet build -c Release
  
  then copy the artifact to package's `./Plugin` directory.
