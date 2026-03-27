// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// https://astro.build/config
export default defineConfig({
	site: 'https://adevcorn.github.io',
	base: '/dotweave',
	integrations: [
		starlight({
			title: 'dotweave',
			description: 'Compile-time OpenTelemetry instrumentation for .NET using C# interceptors and source generators.',
			logo: {
				src: './public/icon.png',
				alt: 'dotweave',
			},
			social: [
				{ icon: 'github', label: 'GitHub', href: 'https://github.com/adevcorn/dotweave' },
			],
			editLink: {
				baseUrl: 'https://github.com/adevcorn/dotweave/edit/main/docs/',
			},
			customCss: ['./src/styles/custom.css'],
			sidebar: [
				{
					label: 'Getting Started',
					items: [
						{ label: 'Introduction', slug: 'guides/introduction' },
						{ label: 'Installation', slug: 'guides/installation' },
						{ label: 'Quick Start', slug: 'guides/quick-start' },
					],
				},
				{
					label: 'Reference',
					items: [
						{ label: '[Traced]', slug: 'reference/traced' },
						{ label: '[Measured]', slug: 'reference/measured' },
						{ label: 'Supported Signatures', slug: 'reference/signatures' },
						{ label: 'Diagnostics', slug: 'reference/diagnostics' },
					],
				},
				{
					label: 'How It Works',
					items: [
						{ label: 'Architecture', slug: 'guides/how-it-works' },
						{ label: 'Demo App', slug: 'guides/demo' },
					],
				},
			],
		}),
	],
});
