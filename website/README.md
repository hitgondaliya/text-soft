# Marketing Website (FR-7)

Responsive, self-contained static site. No build step, no external dependencies —
just `index.html`, `styles.css`, `main.js`. Client provides domain + hosting (FRD §4).

## Preview locally
```bash
cd website
python3 -m http.server 8791
```
Then open http://localhost:8791/.

## Deploy
Upload the three files to any static host (Netlify, Vercel, GitHub Pages, S3,
cPanel, etc.). No server-side code required.

## Sections (map to FR-7)
- Hero with download CTA
- Features overview
- How it works (3 steps)
- Pricing (Free / Premium)
- Download CTA
- Contact

## Placeholders to fill before launch
| Where | What to replace |
|---|---|
| `<link rel="canonical">`, `og:url` | Real production URL (currently `https://example.com/`) |
| Premium price (`₹—`) | Final price |
| Download button (`data-placeholder-link`) | Real installer download URL |
| Contact `mailto:` | Client support email (currently the developer's) |
| Brand name / logo mark (`अ`) | Client branding if different |

## Notes
- Basic on-page SEO: `<title>`, meta description, Open Graph tags, semantic HTML.
- Fully responsive (desktop / tablet / mobile); verified no horizontal scroll.
- Add a real favicon and an `og:image` before launch for better sharing.
