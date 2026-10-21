Materials here are never used directly. Unity strips shader variants that no material in the build uses,
and every material in this game is made in code at runtime, so variants only those runtime materials use
(like the transparent version of URP Lit, for the worn dirt paths) would be missing from a build and the
paths would draw as solid strips. Being in a Resources folder, these are always built, which keeps their
variants in.
