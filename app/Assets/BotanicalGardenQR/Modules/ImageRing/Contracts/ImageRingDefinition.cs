using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace BotanicalGardenQR.ImageRing.Contracts
{
    public sealed class ImageRingItemDefinition
    {
        public ImageRingItemDefinition(
            Texture2D image,
            AudioClip audio,
            string title,
            string description)
        {
            Image = image != null ? image : throw new ArgumentNullException(nameof(image));
            Audio = audio != null ? audio : throw new ArgumentNullException(nameof(audio));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("An image-ring item title is required.", nameof(title));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("An image-ring item description is required.", nameof(description));

            Title = title.Trim();
            Description = description.Trim();
        }

        public Texture2D Image { get; }
        public AudioClip Audio { get; }
        public string Title { get; }
        public string Description { get; }
    }

    public sealed class ImageRingDefinition
    {
        public const int MinimumItemCount = 3;
        public const int MaximumItemCount = 8;

        readonly ReadOnlyCollection<ImageRingItemDefinition> _items;

        public ImageRingDefinition(IReadOnlyList<ImageRingItemDefinition> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (items.Count < MinimumItemCount || items.Count > MaximumItemCount)
                throw new ArgumentOutOfRangeException(
                    nameof(items),
                    $"ImageRing requires {MinimumItemCount} to {MaximumItemCount} items.");

            var copy = new ImageRingItemDefinition[items.Count];
            var textureIds = new HashSet<int>();
            var audioIds = new HashSet<int>();
            for (var index = 0; index < copy.Length; index++)
            {
                var item = items[index] ?? throw new ArgumentException(
                    $"ImageRing item {index} is null.",
                    nameof(items));
                if (!textureIds.Add(item.Image.GetInstanceID()))
                    throw new ArgumentException("ImageRing textures must be unique within one scene.", nameof(items));
                if (!audioIds.Add(item.Audio.GetInstanceID()))
                    throw new ArgumentException("ImageRing audio clips must be unique within one scene.", nameof(items));
                copy[index] = item;
            }

            _items = Array.AsReadOnly(copy);
        }

        public IReadOnlyList<ImageRingItemDefinition> Items => _items;
    }
}
