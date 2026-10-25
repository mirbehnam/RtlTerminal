# Startup directory

Desktop/Start Menu shortcuts and the installer's launch action start in the current user's profile folder, not `Program Files`.

The app also handles shortcuts from older installations: when no explicit folder is supplied and the inherited working directory is the executable folder (or a child of it), use the user profile folder. Do not change the application's process-wide working directory.

An explicit folder requested by the app or passed as the first command-line argument takes precedence, even when that folder is the installation folder. A valid working directory inherited from another terminal is preserved. Invalid directories fall back to the user profile. WSL's existing default Linux-home behavior remains unchanged.

Regression check: `dotnet run --project tests/RtlTerminal.RenderTests -c Release -- --startup-directory-tests`.
