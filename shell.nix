{
  pkgs ? import <nixpkgs> {
    overlays = [
      (import "${builtins.fetchTarball "https://github.com/nadevko/bsuir-TeX-1/archive/master.tar.gz"}/nixpkgs")
    ];
  },
  mkShell ? pkgs.mkShell,
  writeText ? pkgs.writeText,
  clang-tools ? pkgs.clang-tools,
}:
mkShell rec {
  vscode-settings = writeText "settings.json" (
    builtins.toJSON { "clangd.path" = "${clang-tools}/bin/clangd"; }
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
    clang-tools
    cmake-format
    cmake
  ];

  shellHook = ''
    mkdir .vscode &>/dev/null
    cp ${vscode-settings} .vscode/settings.json
  '';
}
