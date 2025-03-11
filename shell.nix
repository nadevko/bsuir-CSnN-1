{
  pkgs ? import <nixpkgs> {
    overlays = [
      (import "${builtins.fetchTarball "https://github.com/nadevko/bsuir-TeX-1/archive/master.tar.gz"}/nixpkgs")
    ];
  },
  mkShell ? pkgs.mkShell,
}:
mkShell {
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
  ];
}
