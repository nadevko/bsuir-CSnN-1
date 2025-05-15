{
  description = "BSUIR: Computer systems and networks, term 4";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/release-24.11";
    mkflake.url = "github:jonascarpay/mkflake";

    bsuir-tex.url = "github:nadevko/bsuir-TeX-1/v0.1";
    bsuir-tex.inputs.nixpkgs.follows = "nixpkgs";

    lw02.url = "path:./lw/02";
    lw02.inputs.nixpkgs.follows = "nixpkgs";
    lw02.inputs.mkflake.follows = "mkflake";

    treefmt-nix.url = "github:numtide/treefmt-nix";
    treefmt-nix.inputs.nixpkgs.follows = "nixpkgs";
  };

  outputs =
    {
      self,
      nixpkgs,
      mkflake,
      bsuir-tex,
      treefmt-nix,
      lw02,
    }:
    mkflake.lib.mkflake {
      perSystem =
        system:
        let
          pkgs = nixpkgs.legacyPackages.${system};
          treefmt =
            (treefmt-nix.lib.evalModule pkgs {
              programs.nixfmt.enable = true;
              programs.nixfmt.strict = true;
              programs.latexindent.enable = true;
              programs.fantomas.enable = true;
            }).config.build;
          dotnet =
            with pkgs.dotnetCorePackages;
            combinePackages [
              dotnet_9.sdk
              dotnet_8.sdk
            ];
        in
        {
          packages = {
            lw02 = lw02.packages.${system}.default;
          };
          devShells = {
            default = pkgs.mkShell {
              buildInputs =
                [ dotnet ]
                ++ (with pkgs; [
                  (texliveFull.withPackages (
                    ps: with ps; [
                      bsuir-tex.packages.${system}.default
                      makecell
                      breqn
                      pgfplots
                    ]
                  ))
                  tex-fmt
                  inkscape-with-extensions
                  python312Packages.pygments
                ]);
            };
          };
          formatter = treefmt.wrapper;
        };
      topLevel.nixosModules = {
        default =
          { config, ... }:
          {
            imports = [ lw02.nixosModules.default ];
          };
        lw02 = lw02.nixosModules.default;
      };
    };
}
