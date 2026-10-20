/**
 * When decorative motion may run (UX §50–52). Off when the visitor asks for reduced motion, and where
 * matchMedia is missing (tests, very old browsers), so nothing ever depends on an animation.
 */
export function motionAllowed(): boolean {
  return (
    typeof matchMedia === 'function' && !matchMedia('(prefers-reduced-motion: reduce)').matches
  );
}

/** A desktop-sized screen with a mouse or trackpad: where smooth scrolling and hover effects belong. */
export function isDesktopPointer(): boolean {
  return (
    typeof matchMedia === 'function' &&
    matchMedia('(min-width: 48rem) and (hover: hover) and (pointer: fine)').matches
  );
}

type GsapModules = {
  readonly gsap: typeof import('gsap').gsap;
  readonly ScrollTrigger: typeof import('gsap/ScrollTrigger').ScrollTrigger;
};

let gsapModules: Promise<GsapModules> | undefined;

/** GSAP with ScrollTrigger, loaded on first use only, so pages without these animations never download it. */
export function loadGsap(): Promise<GsapModules> {
  gsapModules ??= Promise.all([import('gsap'), import('gsap/ScrollTrigger')]).then(
    ([core, scroll]) => {
      core.gsap.registerPlugin(scroll.ScrollTrigger);
      return { gsap: core.gsap, ScrollTrigger: scroll.ScrollTrigger };
    },
  );
  return gsapModules;
}
