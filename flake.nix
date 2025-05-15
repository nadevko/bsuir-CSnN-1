{
  description = "BSUIR: Computer systems and networks, term 4";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/release-24.11";
  inputs.mkflake.url = "github:jonascarpay/mkflake";

  inputs.bsuir-tex.url = "github:nadevko/bsuir-TeX-1/v0.1";
  inputs.bsuir-tex.inputs.nixpkgs.follows = "nixpkgs";

  inputs.lw02.url = "./lw/02";
  inputs.lw02.inputs.nixpkgs.follows = "nixpkgs";
  inputs.lw02.inputs.mkflake.follows = "mkflake";

  outputs =
    {
      self,
      nixpkgs,
      mkflake,
      bsuir-tex,
      lw02,
    }:
    mkflake.lib.mkflake {
      perSystem =
        system:
        let
          pkgs = nixpkgs.legacyPackages.${system};
          dotnet-sdk = with pkgs.dotnetCorePackages; combinePackages [ dotnet_9.sdk dotnet_8.sdk ];
          dotnet-runtime = pkgs.dotnetCorePackages.runtime_8_0;
          dotnetSixTool =
            dllOverride: toolName:
            let
              toolVersion =
                (builtins.fromJSON (builtins.readFile ./.config/dotnet-tools.json)).tools."${toolName}".version;
              sha256 =
                (builtins.head (
                  builtins.filter (elem: elem.pname == toolName) ((import ./deps.nix) { fetchNuGet = x: x; })
                )).sha256;
            in
            pkgs.stdenvNoCC.mkDerivation rec {
              name = toolName;
              version = toolVersion;
              nativeBuildInputs = [ pkgs.makeWrapper ];
              src = pkgs.fetchNuGet {
                inherit version sha256;
                pname = name;
                installPhase = ''mkdir -p $out/bin && cp -r tools/net6.0/any/* $out/bin'';
              };
              installPhase =
                let
                  dll = if isNull dllOverride then name else dllOverride;
                in
                ''
                  runHook preInstall
                  mkdir -p "$out/lib"
                  cp -r ./bin/* "$out/lib"
                  makeWrapper "${dotnet-runtime}/bin/dotnet" "$out/bin/${name}" --add-flags "$out/lib/${dll}.dll"
                  runHook postInstall
                '';
            };
        in
        {
          packages = {
            fantomas = dotnetSixTool null "fantomas";
            lw02 = lw02.packages.${system}.default;
          };
          devShells = {
            default = pkgs.mkShell {
              buildInputs =
                [ dotnet-sdk ]
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
          formatter = pkgs.nixfmt-rfc-style;
        };
    };
}
