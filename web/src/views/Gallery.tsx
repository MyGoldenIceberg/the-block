import { useState } from 'react'

/**
 * The lot's photos.
 *
 * Every one is a placeholder on someone else's host, so each frame keeps its
 * shape whether or not the image arrives and a failure degrades to a labelled
 * space rather than a broken icon. No lightbox: there is nothing to zoom into
 * on a placeholder, and it would be craft spent on the one part of this
 * dataset that is admittedly fake.
 */
export function Gallery({ images, name }: { images: string[]; name: string }) {
  const [active, setActive] = useState(0)
  const [broken, setBroken] = useState<Set<number>>(new Set())

  if (images.length === 0) {
    return <div className="gallery__frame gallery__frame--empty">No photos</div>
  }

  const markBroken = (index: number) => setBroken((current) => new Set(current).add(index))

  return (
    <div className="gallery">
      <div className="gallery__frame">
        {broken.has(active) ? (
          <div className="gallery__missing">Photo unavailable</div>
        ) : (
          <img
            src={images[active]}
            alt={`${name}, photo ${active + 1} of ${images.length}`}
            width={800}
            height={600}
            decoding="async"
            onError={() => markBroken(active)}
          />
        )}
      </div>

      {images.length > 1 && (
        <div className="gallery__strip">
          {images.map((image, index) => (
            <button
              key={image}
              type="button"
              className="gallery__pick"
              aria-label={`Photo ${index + 1}`}
              aria-current={index === active}
              onClick={() => setActive(index)}
            >
              {broken.has(index) ? (
                <span className="gallery__missing" />
              ) : (
                <img
                  src={image}
                  alt=""
                  loading="lazy"
                  decoding="async"
                  width={160}
                  height={120}
                  onError={() => markBroken(index)}
                />
              )}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
