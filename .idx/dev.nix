# To learn more about how to use Nix to configure your environment
# see: https://developers.google.com/idx/guides/customize-idx-env
{ pkgs, ... }: {
  # Which nixpkgs channel to use.
  channel = "stable-23.11"; # or "unstable"

  # Use https://search.nixos.org/packages to find packages
  packages = [
    pkgs.dotnet-sdk_8
    pkgs.openssl
    pkgs.pkg-config
  ];

  # Sets environment variables in the workspace
  env = {};
  # LINT: The 'pre-commit' hook is not defined.
  # To configure automatic formatting on commit, see https://developers.google.com/idx/guides/pre-commit-hooks
  idx = {
    # Search for extensions on the Open VSX Registry -> https://open-vsx.org/
    extensions = [
      "ms-dotnettools.csharp"
      "ms-dotnettools.csdevkit"
    ];

    # Enable previews and customize configuration in your workspace.
    # See https://developers.google.com/idx/guides/customize-idx-env#previews
    previews = {
      enable = true;
    };
  };
}
