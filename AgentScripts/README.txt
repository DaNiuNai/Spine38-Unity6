Spine 3.8 / Unity 6000.3 compatibility validation

Run commands from the repository root in PowerShell. Unity operations use the
unity-cli and unity-pipeline skills. Keep Unity asset and .meta changes inside
the Editor/API workflow; do not edit serialized scene/material files by hand.

Set the target once for the commands below:
  $spineProject = (Get-Location).Path

Core format comparison (requires .NET SDK 10; no Unity Editor required):
  dotnet run --project AgentScripts/CoreFixtureCheck/CoreFixtureCheck.csproj -- $spineProject
This uses the committed JSON and binary fixtures under Assets/SpineCompatibility.
The linked Spine runtime library targets netstandard2.1.

Editor checks (save open scenes and leave Play Mode first):
  unity command script run --file AgentScripts/SpineAudit.cs --entry SpineAudit.Run --project-path $spineProject
  unity command script run --file AgentScripts/SpineRealFixture.cs --entry SpineRealFixture.Compare --project-path $spineProject
  unity command script run --file AgentScripts/SpineEditorChecks.cs --entry SpineEditorChecks.Run --project-path $spineProject
  unity command script run --file AgentScripts/SpineBakingCheck.cs --entry SpineBakingCheck.Run --project-path $spineProject
  unity command script run --file AgentScripts/SpineImportCheck.cs --entry SpineImportCheck.Run --project-path $spineProject

SpineFeatureChecks.Run supports both Edit Mode and Play Mode. SpineInputChecks.Run
and SpineInteractionChecks.Run require Play Mode. The interaction check loads
the SDK sample scenes; leaving Play Mode restores the editor scene.

Scene smoke tests:
  unity command editor set_auto_tick --enabled true --project-path $spineProject
  unity command editor.playmode set_run_in_background --project-path $spineProject
  unity command script eval 'return SpineSceneSmoke.Start("URP");' --project-path $spineProject
Use "Built-in" or "URP-2D" for the other renderer checks. The runner exits Play
Mode and restores the scene/pipeline when it finishes. Reports are written to
Artifacts/SpineCompatibility/scenes-*.json. Restore the editor-session background
override when finished:
  unity command editor.playmode set_run_in_background --enabled false --project-path $spineProject

Platform script compilation (requires the corresponding Unity build module):
  unity command script run --file AgentScripts/SpinePlatformCompile.cs --entry SpinePlatformCompile.Compile --args '["Android"]' --project-path $spineProject
Other target names: StandaloneWindows64, iOS, WebGL. These are script compilation
checks, not full Player builds or device tests.

SpineBuiltinValidation.Enable temporarily selects Built-in/Gamma for validation
of the original PMA examples. Always call SpineBuiltinValidation.Restore after
that validation/build. Its restoration snapshot is stored in .tmp.

Export packages:
  unity command script run --file AgentScripts/SpineExport.cs --entry SpineExport.Run --project-path $spineProject
Generated archives are in Artifacts/SpineCompatibility/Packages and are ignored
by Git. packages.json records hashes of the delivered archives from this run.

WebGL preview after building:
  node AgentScripts/serve-webgl.mjs Artifacts/SpineCompatibility/Builds/WebGLURP
Open the loopback URL printed by the server. It serves Brotli/GZip files with the
required headers and leaves opening the browser to the user.

SpineRealFixture.Import is an initial-import helper for the original exports in
.tmp/测试/测试json and .tmp/测试/测试二进制. It refuses to overwrite the committed
fixture assets; normal regression runs use Compare instead.

Full build logs, Player assemblies, binaries and archives remain local. The
committed validation-summary.json and focused test reports record the results,
including the outstanding Mac/Xcode/device verification for iOS.
