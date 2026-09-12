# Sequencer Item Recovery: source readability

Keep the explanatory source comments when changing this plugin. New or changed
classes, methods, safety checks, N.I.N.A. integration points, JSON paths, and
non-obvious control flow receive plain-English comments that explain their
purpose and safety reason; do not replace them with comments that merely repeat
the code.

Keep source comments and visible XAML help text wrapped at about 80 columns,
breaking at the next useful space. Format XAML as one normal, indented source
column. The visible N.I.N.A. recovery table remains a functional three-column
table: timestamp, instruction, and Restore action.
