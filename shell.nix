{
  pkgs ? import <nixpkgs> {
    overlays = [
      (import "${builtins.fetchTarball "https://github.com/nadevko/bsuir-TeX-1/archive/master.tar.gz"}/nixpkgs")
    ];
  },
  mkShell ? pkgs.mkShell,
  writeText ? pkgs.writeText,
  xorg ? pkgs.xorg,
  dotnetCorePackages ? pkgs.dotnetCorePackages,
  fontconfig ? pkgs.fontconfig,
}:
let
  sdk = with dotnetCorePackages; combinePackages [ sdk_8_0 ];
  deps = [
    xorg.libX11
    xorg.libICE
    xorg.libSM
    fontconfig
  ];
in
mkShell rec {
  vscode-settings = writeText "settings.json" (
    builtins.toJSON {
      "dotnetAcquisitionExtension.sharedExistingDotnetPath" = DOTNET_ROOT;
    }
  );

  packages = with pkgs; [
    (texliveFull.withPackages (
      ps: with ps; [
        texlivePackages.bsuir-tex
        makecell
        breqn
        pgfplots
      ]
    ))
    tex-fmt
    inkscape-with-extensions
    python312Packages.pygments

    sdk
  ] ++ deps;
  LD_LIBRARY_PATH = with pkgs; lib.makeLibraryPath deps;
  DOTNET_ROOT = "${sdk}/dotnet";

  shellHook = ''
    mkdir .vscode &>/dev/null
    cp --force ${vscode-settings} .vscode/settings.json
  '';
}
