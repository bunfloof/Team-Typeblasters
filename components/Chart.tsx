"use client";
// An Apache ECharts chart. Pass a full ECharts option; it re-renders when the
// option object changes (memoise it with useMemo) and follows its box size.
//   <Chart option={option} height={320} />
// group: charts with the same group share their tooltip / zoom (echarts.connect).
import { useEffect, useRef } from "react";
import type { ECharts, EChartsOption } from "echarts";

type Props = {
  option: EChartsOption;
  height?: number | string;
  className?: string;
  group?: string;
  // ECharts events, e.g. { click: (params) => ... }
  onEvents?: Record<string, (params: unknown) => void>;
  onReady?: (chart: ECharts) => void;
  ariaLabel?: string;
};

export default function Chart({ option, height = 320, className, group, onEvents, onReady, ariaLabel }: Props) {
  const boxRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<ECharts | null>(null);
  const optionRef = useRef(option);
  const eventsRef = useRef(onEvents);
  const readyRef = useRef(onReady);

  useEffect(() => {
    optionRef.current = option;
    eventsRef.current = onEvents;
    readyRef.current = onReady;
  });

  // Create the chart once (ECharts only runs in the browser).
  useEffect(() => {
    let disposed = false;
    let observer: ResizeObserver | null = null;
    import("echarts").then((echarts) => {
      const box = boxRef.current;
      if (disposed || !box) {
        return;
      }
      const chart = echarts.init(box, null, { renderer: "canvas" });
      chartRef.current = chart;
      chart.setOption(optionRef.current, true);
      if (group) {
        chart.group = group;
        echarts.connect(group);
      }
      // Handlers are looked up at call time, so a re-render with new ones works
      // (the event NAMES are taken from the first render).
      for (const name of Object.keys(eventsRef.current || {})) {
        chart.on(name, (params: unknown) => eventsRef.current?.[name]?.(params));
      }
      readyRef.current?.(chart);
      observer = new ResizeObserver(() => chart.resize());
      observer.observe(box);
    });
    return () => {
      disposed = true;
      observer?.disconnect();
      chartRef.current?.dispose();
      chartRef.current = null;
    };
  }, [group]);

  // A new option replaces the old one.
  useEffect(() => {
    chartRef.current?.setOption(option, true);
  }, [option]);

  return <div ref={boxRef} className={className} style={{ width: "100%", height }} role="img" aria-label={ariaLabel} />;
}
