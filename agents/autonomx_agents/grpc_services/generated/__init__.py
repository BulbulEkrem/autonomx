"""Generated gRPC stubs from .proto files.

Run `make proto` to regenerate after changing .proto files.

The generated code uses bare imports (e.g., `import common_pb2`).
We add the generated directory to sys.path so these imports resolve correctly.
"""

import sys
from pathlib import Path

# Ensure generated directory is on sys.path for bare imports in generated code
_generated_dir = str(Path(__file__).parent)
if _generated_dir not in sys.path:
    sys.path.insert(0, _generated_dir)
