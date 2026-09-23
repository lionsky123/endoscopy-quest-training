"""Targeted read-only checks for the source FBX audit."""
import struct
import unittest
import zlib
from pathlib import Path

import numpy as np

from audit_scope_fbx import Reader, audit, topology


HEADER = b"Kaydara FBX Binary  \x00\x1a\x00" + struct.pack("<I", 7400)


class SourceAuditTests(unittest.TestCase):
    def test_wrong_magic_rejected(self):
        with self.assertRaises(ValueError):
            Reader(b"not an fbx")

    def test_truncated_node_rejected(self):
        with self.assertRaises(ValueError):
            Reader(HEADER + b"\x00").node()

    def test_compressed_array_exact_values(self):
        raw = struct.pack("<3i", 0, 1, -3)
        packed = zlib.compress(raw)
        reader = Reader(HEADER + b"i" + struct.pack("<III", 3, 1, len(packed)) + packed)
        np.testing.assert_array_equal(reader.prop(), [0, 1, -3])

    def test_oversized_array_rejected_before_allocation(self):
        reader = Reader(HEADER + b"d" + struct.pack("<III", 100000000, 0, 0))
        with self.assertRaises(ValueError):
            reader.prop()

    def test_position_weld_is_audit_only(self):
        points = np.array([[0., 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 0], [-1, 0, 0], [0, -1, 0]])
        original = points.copy()
        faces = np.array([0, 1, -3, 3, 4, -6])
        self.assertEqual(topology(points, faces)["components"], 2)
        self.assertEqual(topology(points, faces, True)["components"], 1)
        np.testing.assert_array_equal(points, original)

    def test_bad_polygon_index_rejected(self):
        with self.assertRaises(ValueError):
            topology(np.array([[0., 0, 0]]), np.array([-3]))

    def test_actual_scope_matches_existing_unity_triangle_counts(self):
        source = Path(__file__).resolve().parents[1] / "app/Assets/EndoscopyTheme/ImportedModels/Source/Gastroscope/Gastroscope.fbx"
        report = audit(source)
        self.assertEqual(report["sha256"], "e288bd041556a8ecb9c4faf22113082528aeb0b6d07089cdb8e436309d8626b0")
        self.assertEqual(report["deformers"], [])
        self.assertEqual(len(report["geometry"]), 2)
        triangle_counts = sorted(sum((int(size) - 2) * count for size, count in item["indexed"]["polygon_sizes"].items()) for item in report["geometry"])
        self.assertEqual(triangle_counts, [1920, 97152])
        self.assertTrue(all(item["position_welded_audit_only"]["components"] == 1 for item in report["geometry"]))


if __name__ == "__main__":
    unittest.main(verbosity=2)
