{
  description = "A very basic flake";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs";
    dev_flake.url = "github:nicolasgermann/dev_flake";
  };

  outputs = { self, nixpkgs, dev_flake }:
    let
      forAllSystems = nixpkgs.lib.genAttrs [
        "aarch64-darwin"
        "x86_64-darwin"
        "aarch64-linux"
        "x86_64-linux"
      ];
    in
    {
      devShells = forAllSystems (system:
        let
          pkgs = import nixpkgs { inherit system; };
          # If you want to include dev_flake's devShell, you might need:
          # baseShell = dev_flake.devShells.${system}.default;
          # For now, let's assume no baseShell:
	  baseShell = dev_flake.devShells.${system}.dotnet;
        in
        {
          default = pkgs.mkShell {
            buildInputs = baseShell.buildInputs ++ [
              pkgs.mosquitto
            ];
            shellHook = baseShell.shellHook;
          };
        }
      );
    };
}

