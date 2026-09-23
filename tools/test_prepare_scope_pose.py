"""Geometry gate checks; a failed draft is not a runtime-ready resource."""
import unittest
import numpy as np
from prepare_scope_pose import deformation_metrics, draft_geometry_valid, pose, rotate, smooth_curve, INSERTION


class PoseTests(unittest.TestCase):
    def setUp(self):
        self.points = np.array([[0., 0, 0], [1, 0, 0], [0, 1, 0]])
        self.faces = np.array([0, 1, -3])

    def test_rigid_transform_preserves_geometry(self):
        after = self.points.copy()
        after[:, :2] = rotate(after[:, :2], .7) + 3
        report = deformation_metrics(self.points, after, self.faces)
        self.assertTrue(report["geometry_gate_passed"])
        self.assertAlmostEqual(report["edge_length_ratio_max"], 1.)

    def test_stretch_is_rejected(self):
        after = self.points.copy()
        after[1] *= 10
        report = deformation_metrics(self.points, after, self.faces)
        self.assertFalse(report["geometry_gate_passed"])
        self.assertGreater(report["edges_stretched_over_2x"], 0)

    def test_collapse_is_rejected(self):
        report = deformation_metrics(self.points, np.zeros_like(self.points), self.faces)
        self.assertFalse(report["geometry_gate_passed"])
        self.assertEqual(report["newly_degenerate_triangles"], 1)

    def test_label_rigid_and_source_unchanged(self):
        before = self.points.copy()
        after = pose(self.points, label=True)
        np.testing.assert_array_equal(self.points, before)
        self.assertTrue(deformation_metrics(before, after, self.faces)["geometry_gate_passed"])

    def test_curve_preserves_endpoints(self):
        curve = smooth_curve(INSERTION)
        np.testing.assert_allclose(curve[0], INSERTION[0])
        np.testing.assert_allclose(curve[-1], INSERTION[-1])

    def test_one_valid_mesh_does_not_approve_invalid_body(self):
        self.assertFalse(draft_geometry_valid({"meshes": [{"geometry_gate_passed": False}, {"geometry_gate_passed": True}]}))

    def test_empty_or_missing_results_fail_closed(self):
        self.assertFalse(draft_geometry_valid({"meshes": []}))
        self.assertFalse(draft_geometry_valid({"meshes": [{}]}))
        self.assertTrue(draft_geometry_valid({"meshes": [{"geometry_gate_passed": True}]}))


if __name__ == "__main__":
    unittest.main(verbosity=2)
