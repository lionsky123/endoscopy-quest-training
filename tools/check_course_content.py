"""Validate the active C09 curriculum and local media without launching Unity."""
from pathlib import Path
import json
import math

ROOT = Path(__file__).resolve().parents[1]
RESOURCES = ROOT / "app/Assets/EndoscopyTheme/Resources"
EXPECTED = {
    "baobab": (2, ["sequence", "ledger"]),
    "bottle_tree": (3, ["video"]),
    "ceiba": (4, ["image", "evidence"]),
    "macrozamia": (5, ["model", "evidence"]),
    "welwitschia": (6, ["ledger", "ledger"]),
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def text(value):
    return isinstance(value, str) and bool(value.strip())


def media(resource, extensions):
    require(text(resource), "Empty media path")
    stem = RESOURCES / resource
    require(stem.resolve().is_relative_to(RESOURCES.resolve()), f"Media outside Resources: {resource}")
    candidates = [stem.with_suffix(ext) for ext in extensions if stem.with_suffix(ext).is_file()]
    require(len(candidates) == 1, f"Missing or ambiguous media: {resource}")
    require(Path(str(candidates[0]) + ".meta").is_file(), f"Missing Unity meta: {resource}")


def main():
    observation = json.loads((RESOURCES / "ClinicalEvidence/lesson.json").read_text(encoding="utf-8-sig"))
    topics = observation["topics"]
    require(len(topics) == 3, "P01 must contain three guided observations")
    for topic in topics:
        require(all(text(topic.get(key)) for key in ("title", "method")), "Incomplete observation copy")
        uv = topic["panoramaUv"]
        require(all(isinstance(uv.get(axis), (int, float)) and math.isfinite(uv[axis]) and 0 <= uv[axis] <= 1 for axis in ("x", "y")), "Invalid panorama direction")
    media("ClinicalEvidence/InspectionMagnifier", (".fbx", ".obj", ".prefab"))
    media("ClinicalEvidence/InspectionMagnifierDiffuse", (".png", ".jpg"))
    lessons = json.loads((RESOURCES / "ClinicalCourse/course.json").read_text(encoding="utf-8-sig"))["lessons"]
    require(len(lessons) == 5, "Expected five later stations")
    require({lesson["sceneId"] for lesson in lessons} == set(EXPECTED), "Unknown, duplicate or missing station")
    for lesson in lessons:
        scene = lesson["sceneId"]
        station, modes = EXPECTED[scene]
        require(lesson["station"] == station, f"Station binding mismatch: {scene}")
        require(all(text(lesson.get(key)) for key in ("title", "introduction")), f"Missing introduction: {scene}")
        require([step["mode"] for step in lesson["steps"]] == modes, f"Task count/modes changed: {scene}")
        for step in lesson["steps"]:
            label = f"{scene}: {step.get('title')}"
            require(all(text(step.get(key)) for key in ("title", "credit", "body", "prompt", "hint", "explanation")), f"Incomplete content: {label}")
            mode = step["mode"]
            if mode == "sequence":
                items, order = step["sequenceItems"], step["sequenceOrder"]
                require(len(items) == len(set(items)) == 5 and all(text(item) for item in items), f"Invalid process cards: {label}")
                require(len(order) == 5 and set(order) == set(range(5)), f"Invalid process order: {label}")
            else:
                choices = step["options"]
                require(len(choices) == len(set(choices)) == 3 and all(text(choice) for choice in choices), f"Invalid choices: {label}")
                require(type(step["correct"]) is int and 0 <= step["correct"] < 3, f"Invalid answer index: {label}")
            if mode == "evidence":
                require(len(step["cards"]) == 3 and all(text(card.get("title")) and text(card.get("body")) for card in step["cards"]), f"Incomplete evidence pages: {label}")
            extensions = {"image": (".png", ".jpg"), "video": (".mp4",), "model": (".obj", ".fbx", ".prefab")}
            if mode in extensions:
                media(step["media"], extensions[mode])
    for source in ("ClinicalCourse/SOURCES.md", "ClinicalEvidence/OBSERVATION_TOKEN_SOURCE.md"):
        require((RESOURCES / source).is_file(), f"Missing source record: {source}")
    total = len(topics) + sum(len(lesson["steps"]) for lesson in lessons)
    require(total == 12, f"Expected 12 current activities, got {total}")
    print("PASS: C09 6 stations / 3 guided observations + 9 later tasks; bindings, modes, text, evidence, local media/metas and source records.")


if __name__ == "__main__":
    main()
