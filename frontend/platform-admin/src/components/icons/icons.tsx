import React from "react";

export type IconProps = {
  size?: number;
  title?: string;
  className?: string;
};

function svgProps({ size = 16, title, className }: IconProps) {
  return {
    width: size,
    height: size,
    viewBox: "0 0 24 24",
    fill: "none",
    xmlns: "http://www.w3.org/2000/svg",
    className,
    "aria-hidden": title ? undefined : true,
    role: title ? ("img" as const) : undefined,
  } as const;
}

export function IconSpinner(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2" strokeOpacity="0.2" />
      <g>
        <animateTransform
          attributeName="transform"
          attributeType="XML"
          type="rotate"
          from="0 12 12"
          to="360 12 12"
          dur="0.75s"
          repeatCount="indefinite"
        />
        <path
          d="M12 3a9 9 0 0 1 9 9"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
        />
      </g>
    </svg>
  );
}

export function IconCircleAlert(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2" />
      <path d="M12 8v5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <circle cx="12" cy="16" r="1" fill="currentColor" />
    </svg>
  );
}

export function IconCircleCheck(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2" />
      <path d="M8 12l2.5 2.5L16 9" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function IconInbox(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <path
        d="M4 10h3l1.5-2h7L17 10h3v8H4v-8z"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinejoin="round"
      />
      <path d="M4 10l2-4h12l2 4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function IconLayers(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <path d="M12 4 4 8l8 4 8-4-8-4Z" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" />
      <path d="m4 12 8 4 8-4M4 16l8 4 8-4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function IconMenu(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <path d="M5 7h14M5 12h14M5 17h14" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

export function IconLogOut(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <path d="M10 17H6a2 2 0 0 1-2-2V9a2 2 0 0 1 2-2h4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M14 15l4-3-4-3M18 12H9" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function IconPalette(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <path
        d="M12 3a7 7 0 1 0 7 10h-4a2 2 0 0 0-4 0H7a5 5 0 1 1 5-10Z"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinejoin="round"
      />
      <circle cx="8" cy="9" r="1" fill="currentColor" />
      <circle cx="10" cy="6" r="1" fill="currentColor" />
      <circle cx="14" cy="6" r="1" fill="currentColor" />
      <circle cx="16" cy="9" r="1" fill="currentColor" />
    </svg>
  );
}

export function IconGlobe(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2" />
      <path d="M3 12h18M12 3a16 16 0 0 1 0 18M12 3a16 16 0 0 0 0 18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

export function IconLock(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <rect x="6" y="11" width="12" height="10" rx="2" stroke="currentColor" strokeWidth="2" />
      <path d="M9 11V8a3 3 0 0 1 6 0v3" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

export function IconSun(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <circle cx="12" cy="12" r="4" stroke="currentColor" strokeWidth="2" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

export function IconMoon(props: IconProps) {
  const p = svgProps(props);
  return (
    <svg {...p}>
      {props.title ? <title>{props.title}</title> : null}
      <path
        d="M20 14A8 8 0 1 1 10 4a6.5 6.5 0 0 0 10 10Z"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinejoin="round"
      />
    </svg>
  );
}
