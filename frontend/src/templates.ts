/** Starter workflows: pre-wired graphs with sensible configs. Created via the
 *  same create + update calls as manual authoring. */

export interface TemplateNode {
  nodeId: string
  nodeType: string
  label: string
  config: Record<string, unknown>
  positionX: number
  positionY: number
}

export interface TemplateEdge {
  sourceNodeId: string
  targetNodeId: string
}

export interface WorkflowTemplate {
  name: string
  description: string
  nodes: TemplateNode[]
  edges: TemplateEdge[]
}

export const TEMPLATES: WorkflowTemplate[] = [
  {
    name: "API ingestion & clean",
    description: "Pull JSON from an API, keep good rows, save the result. Runs on a schedule.",
    nodes: [
      {
        nodeId: "fetch", nodeType: "http.request", label: "Fetch",
        config: { url: "https://dummyjson.com/products", rootPath: "products" },
        positionX: 60, positionY: 140,
      },
      {
        nodeId: "quality", nodeType: "data.validate", label: "Quality",
        config: { requiredColumns: ["id", "title", "price"], columnTypes: { price: "number" }, uniqueColumns: ["id"] },
        positionX: 324, positionY: 140,
      },
      {
        nodeId: "cheap", nodeType: "data.filter", label: "Cheap only",
        config: { column: "price", operator: "lessThan", value: 100 },
        positionX: 588, positionY: 140,
      },
      {
        nodeId: "out", nodeType: "data.output", label: "Save",
        config: { format: "json" },
        positionX: 852, positionY: 140,
      },
    ],
    edges: [
      { sourceNodeId: "fetch", targetNodeId: "quality" },
      { sourceNodeId: "quality", targetNodeId: "cheap" },
      { sourceNodeId: "cheap", targetNodeId: "out" },
    ],
  },
  {
    name: "CSV cleanup",
    description: "Paste CSV, validate, drop duplicates, download the clean file.",
    nodes: [
      {
        nodeId: "read", nodeType: "data.csv.read", label: "Read CSV",
        config: { source: "text", csvText: "name,age,city\nAda,36,Berlin\nBob,15,Paris\nAda,36,Berlin", delimiter: ",", hasHeader: true, skipRows: 0, trim: true, nullValues: [], maxRows: 50000 },
        positionX: 60, positionY: 140,
      },
      {
        nodeId: "quality", nodeType: "data.validate", label: "Quality",
        config: { requiredColumns: ["name", "age"], columnTypes: { age: "integer" }, uniqueColumns: [] },
        positionX: 324, positionY: 140,
      },
      {
        nodeId: "unique", nodeType: "data.dedupe", label: "Dedupe",
        config: { columns: [] },
        positionX: 588, positionY: 140,
      },
      {
        nodeId: "out", nodeType: "data.output", label: "Save",
        config: { format: "csv", includeHeader: true },
        positionX: 852, positionY: 140,
      },
    ],
    edges: [
      { sourceNodeId: "read", targetNodeId: "quality" },
      { sourceNodeId: "quality", targetNodeId: "unique" },
      { sourceNodeId: "unique", targetNodeId: "out" },
    ],
  },
  {
    name: "Webhook filter",
    description: "Receive a JSON webhook, keep matching rows, save. Add a webhook trigger after creating.",
    nodes: [
      {
        nodeId: "pick", nodeType: "trigger.payload", label: "Payload",
        config: { rootPath: "" },
        positionX: 60, positionY: 140,
      },
      {
        nodeId: "adults", nodeType: "data.filter", label: "Adults",
        config: { column: "age", operator: "greaterThan", value: "{{ trigger.body.minAge }}" },
        positionX: 324, positionY: 140,
      },
      {
        nodeId: "out", nodeType: "data.output", label: "Save",
        config: { format: "json" },
        positionX: 588, positionY: 140,
      },
    ],
    edges: [
      { sourceNodeId: "pick", targetNodeId: "adults" },
      { sourceNodeId: "adults", targetNodeId: "out" },
    ],
  },
]
