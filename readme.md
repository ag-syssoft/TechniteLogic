TechniteLogic
=============

Requirements
------------
This client is implemented in C#. Mono-compatibility is intended but may not be regularly checked. Create an issue if problems come up.
A Technite world server is required to implement global world rules. A download link to an existing C++ implementation will be added shortly, Open Source implementations will follow in due time.

Technites
---------
Technites are small volumetric cell entities that have limited perception and instruction sets, but can implement a multitude of structural and logic strategies.
Currently supported operations allow to eat neighboring volume cells, split into them, and transfer resources to neighboring Technites.
Technites can not move at this time, to allow addressing them via their location.

Functionality of this base implementation
-----------------------------------------
Communication protocols, world and Technite states, as well as helper methods to simplify logic implementation are, or will be, part of this project.
With the exception of a simple base logic, no advanced logic implementations will be part of this repository.
