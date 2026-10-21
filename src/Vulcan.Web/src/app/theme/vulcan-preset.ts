import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

/**
 * Vulcan theme: PrimeNG Aura with Vulcan navy (#263A78) as the primary color
 * and the neutral grays from the hi-fi mockup.
 * Change the theme here only. Components must not set their own colors:
 * use PrimeNG tokens, e.g. var(--p-text-muted-color) or var(--p-vulcan-link-color).
 */
export const VulcanPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#f4f5f8',
      100: '#e5e7ef',
      200: '#c9cedd',
      300: '#a4acc6',
      400: '#727fa7',
      500: '#263a78',
      600: '#203166',
      700: '#1b2954',
      800: '#152042',
      900: '#0f1730',
      950: '#0a0e1e',
    },
    colorScheme: {
      light: {
        // Neutral grays sampled from the hi-fi mockup (Aura's default is a blue-gray).
        surface: {
          0: '#ffffff',
          50: '#f4f4f4', // page background
          100: '#eff0f2',
          200: '#dddfe4', // borders
          300: '#c4c7cd',
          400: '#9da1a7',
          500: '#696e79', // muted text
          600: '#545964',
          700: '#3d4450', // icons
          800: '#2d3139',
          900: '#21242b', // main text
          950: '#15171b',
        },
        text: {
          color: '{surface.900}',
          hoverColor: '{surface.950}',
          mutedColor: '{surface.500}',
          hoverMutedColor: '{surface.600}',
        },
      },
    },
  },
  extend: {
    vulcan: {
      // Side menu links and other navigation text (mockup: brighter than the brand navy).
      link: { color: '#123b93' },
      // Shell sizes from the mockup.
      header: { height: '56px' },
      sidebar: { width: '254px' },
    },
  },
});
