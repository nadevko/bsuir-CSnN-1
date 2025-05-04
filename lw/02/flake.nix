{
  inputs.nixpkgs.url = "github:NixOS/nixpkgs/release-24.11";
  inputs.mkflake.url = "github:jonascarpay/mkflake";

  outputs =
    { nixpkgs, mkflake, ... }:
    mkflake.lib.mkflake {
      perSystem =
        system: with nixpkgs.legacyPackages.${system}; {
          packages.default = buildDotnetModule {
            projectFile = "./lw02.fsproj";
            dotnet-sdk = dotnet-sdk_8;
            dotnet-runtime = dotnetCorePackages.runtime_8_0;
            pname = "lw02";
            version = "0.1";
            src = ./.;
            nugetDeps = ./deps.nix;
            doCheck = true;
          };
        };
    };
}
