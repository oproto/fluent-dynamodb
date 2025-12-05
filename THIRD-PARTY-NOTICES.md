# Third-Party Notices

This project incorporates material from the projects listed below. The original copyright notices and licenses under which we received such material are set forth below.

---

## S2 Geometry Library

The S2 spatial indexing implementation in `Oproto.FluentDynamoDb.Geospatial/S2/` is based on algorithms from Google's S2 Geometry Library.

**Original Project:**
- Google S2 Geometry Library
- https://github.com/google/s2geometry

**C# Port Reference:**
- s2-geometry-library-csharp
- https://github.com/alas/s2-geometry-library-csharp

**License:** Apache License 2.0

**Copyright Notice:**
```
Copyright 2005 Google Inc.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

**Attribution:**
The S2 encoding algorithms, Hilbert curve transformations, and coordinate projection methods in this library are derived from the S2 Geometry Library. The implementation has been independently written for this project but follows the mathematical algorithms and approaches documented in the original S2 library.

---

## H3 Hexagonal Hierarchical Spatial Index

The H3 spatial indexing implementation in `Oproto.FluentDynamoDb.Geospatial/H3/` is based on algorithms from Uber's H3 library.

**Original Project:**
- Uber H3
- https://github.com/uber/h3

**License:** Apache License 2.0

**Copyright Notice:**
```
Copyright 2018 Uber Technologies, Inc.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

**Attribution:**
The H3 encoding algorithms, hexagonal grid transformations, base cell neighbor tables, and coordinate system conversions in this library are derived from the H3 library. The implementation has been independently written in C# for this project but follows the mathematical algorithms and data structures documented in the original H3 library.

Specifically, the following components are derived from H3:
- **H3Encoder.cs**: Core encoding/decoding algorithms, IJK coordinate system, face-centered coordinate transformations, base cell data tables, and neighbor lookup tables
- **H3Cell.cs**: Cell structure and resolution extraction from H3 index format
- **H3CellCovering.cs**: Cell covering algorithms for spatial queries using hexagonal grid properties

---

## Apache License 2.0

The full text of the Apache License 2.0 can be found at:
http://www.apache.org/licenses/LICENSE-2.0
