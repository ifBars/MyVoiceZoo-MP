# MVZ-MP

- Use the local skills in `.agents/skills` when their scope fits. The general C# skill is copied from the ScheduleOne workspace; the MVZ skill adapts its IL2CPP and testing practices to this game.
- Work from the installed MyVoiceZoo build and generated IL2CPP wrappers. Verify behavior in the live game before calling a feature working.
- Keep game assemblies, generated wrappers, save files, recordings, exported assets, and copied installs out of Git and release packages.
- Build against `local.build.props`; do not commit that file or machine-specific paths in project files.
- Steam lobby ownership and the zoo state host must agree. Guests must not write over their own save as a side effect of joining.
- Separate compile, single-process runtime, and two-account Steam validation in reports.

