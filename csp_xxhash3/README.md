# Custom (old) version of xxhash3 0.7.0

This is only in here for compatibility with CSP. CSP uses this version to generate keys for Lua online events.  
Unfortunately the version of xxhash3 in System.IO.Hashing is incompatible, so we are forced to keep this around...

Can be built with MSVC or g++ like so:

Linux x64
```
g++ -std=c++20 -fPIC -shared xxh3.cpp -o libcsp_xxhash3.so
```

Linux arm64
```
aarch64-linux-gnu-g++ -std=c++20 -fPIC -shared xxh3.cpp -o libcsp_xxhash3_arm64.so
```

Prebuilt binaries are included in the `AssettoServer/Redist` folder.
