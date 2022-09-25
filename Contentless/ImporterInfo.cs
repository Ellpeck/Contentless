using System;
using Microsoft.Xna.Framework.Content.Pipeline;

namespace Contentless; 

public class ImporterInfo {

    public readonly ContentImporterAttribute Importer;
    public readonly Type Type;

    public ImporterInfo(ContentImporterAttribute importer, Type type) {
        this.Importer = importer;
        this.Type = type;
    }

    public override string ToString() {
        return this.Type.Name;
    }

}