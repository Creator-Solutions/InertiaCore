import { defineConfig } from 'vitepress'

// Update `hostname` to the production URL before the first deploy so the
// sitemap and canonical/OG tags point at the right origin.
const HOSTNAME = 'https://inertiacore.dev'

export default defineConfig({
  title: 'InertiaCore',
  description: 'A production-ready Inertia.js adapter for ASP.NET Core.',

  // Served at the root of a dedicated domain.
  base: '/',
  cleanUrls: true,

  lastUpdated: true,

  head: [
    ['link', { rel: 'icon', href: '/favicon.svg' }],
    ['meta', { name: 'theme-color', content: '#9553e9' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'InertiaCore' }],
    [
      'meta',
      {
        property: 'og:description',
        content: 'A production-ready Inertia.js adapter for ASP.NET Core.'
      }
    ]
  ],

  sitemap: {
    hostname: HOSTNAME
  },

  themeConfig: {
    logo: '/logo.svg',

    nav: [
      { text: 'Guide', link: '/introduction', activeMatch: '^/(introduction|getting-started|guide)' },
      { text: 'TypeScript', link: '/typescript/', activeMatch: '^/typescript' },
      { text: 'Security', link: '/security', activeMatch: '^/security' },
      { text: 'Reference', link: '/reference/configuration', activeMatch: '^/reference' },
      { text: 'Changelog', link: '/project/changelog', activeMatch: '^/project' }
    ],

    sidebar: [
      {
        text: 'Getting started',
        items: [
          { text: 'Introduction', link: '/introduction' },
          { text: 'Installation', link: '/getting-started' }
        ]
      },
      {
        text: 'Guide',
        items: [
          { text: 'Pages & responses', link: '/guide/responses' },
          { text: 'Props', link: '/guide/props' },
          { text: 'Shared data', link: '/guide/shared-data' },
          { text: 'Asset versioning', link: '/guide/versioning' },
          { text: 'Validation', link: '/guide/validation' },
          { text: 'Precognition', link: '/guide/precognition' },
          { text: 'Server-side rendering', link: '/guide/ssr' },
          { text: 'Vite integration', link: '/guide/vite' },
          { text: 'Advanced', link: '/guide/advanced' }
        ]
      },
      {
        text: 'TypeScript',
        items: [
          { text: 'Overview', link: '/typescript/' },
          { text: 'Declarations', link: '/typescript/declarations' },
          { text: 'Typed routes', link: '/typescript/routes' }
        ]
      },
      {
        text: 'Security',
        items: [{ text: 'CSRF & hardening', link: '/security' }]
      },
      {
        text: 'Reference',
        items: [{ text: 'Configuration', link: '/reference/configuration' }]
      },
      {
        text: 'Project',
        items: [
          { text: 'Changelog', link: '/project/changelog' },
          { text: 'Roadmap', link: '/project/roadmap' },
          { text: 'Contributing', link: '/project/contributing' }
        ]
      }
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/Creator-Solutions/InertiaCore' }
    ],

    editLink: {
      pattern: 'https://github.com/Creator-Solutions/InertiaCore/edit/main/docs/:path',
      text: 'Edit this page on GitHub'
    },

    search: {
      provider: 'local'
    },

    outline: {
      level: [2, 3],
      label: 'On this page'
    },

    docFooter: {
      prev: 'Previous',
      next: 'Next'
    },

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'InertiaCore contributors'
    }
  }
})
