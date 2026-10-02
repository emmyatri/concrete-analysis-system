import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

/**
 * Vulcan theme: PrimeNG Aura with Vulcan navy (#263A78) as the primary color.
 * 500 is the brand color. Lighter and darker steps are tints and shades of it.
 * Change the theme here only. Components must NOT set their own brand colors.
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
  },
});
